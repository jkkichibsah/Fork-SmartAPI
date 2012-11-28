﻿/*
 * Smart API - .Net programatical access to RedDot servers
 * Copyright (C) 2012  erminas GbR 
 *
 * This program is free software: you can redistribute it and/or modify it 
 * under the terms of the GNU General Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See the GNU General Public License for more details. 
 *
 * You should have received a copy of the GNU General Public License along with this program.
 * If not, see <http://www.gnu.org/licenses/>. 
 */

using System;
using System.Xml;

namespace erminas.SmartAPI.CMS.PageElements
{
    public abstract class StandardField<T> : AbstractValueElement<T>
    {
        private string _description;
        private string _sample;

        protected StandardField(Project project, XmlNode node)
            : base(project, node)
        {
        }

        protected StandardField(Project project, Guid guid)
            : base(project, guid)
        {
        }

        public string SampleText
        {
            get { return LazyLoad(ref _sample); }
        }

        public string Description
        {
            get { return LazyLoad(ref _description); }
        }

        protected override void LoadXml(XmlNode node)
        {
            base.LoadXml(node);

            InitIfPresent(ref _sample, "eltrdsample", x => x);
            InitIfPresent(ref _sample, "eltrddescription", x => x);
            InitIfPresent(ref _value, "value", FromXmlNodeValue);
        }

        protected virtual T FromXmlNodeValue(string value)
        {
            return FromString(value);
        }
    }
}